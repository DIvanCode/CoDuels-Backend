#include <iostream>
#include <algorithm>
#include <math.h>
#include <vector>
using namespace std;
int n,inf=1e9;
vector<long long> segment_tree,ar;
void build(int now,int begin,int end)
{
	if (end==begin)
	{
		segment_tree[now]=end;
	}
	else 
	{
		build(now*2,begin,(begin+end)/2);
		build(now*2+1,(begin+end)/2+1,end);
		if (ar[segment_tree[now*2]]>ar[segment_tree[now*2+1]]) segment_tree[now]=segment_tree[now*2];
		else segment_tree[now]=segment_tree[now*2+1];
	}
}
int maxsegment(int now,int left,int right,int begin,int end)
{
	if (end<begin)
	{
		return -inf;
	}
	else if ((left==begin)&&(right==end))
	{
		return segment_tree[now];
	}
	else 
	{
		int middle=(left+right)/2;
		int x=maxsegment(now*2,left,middle,begin,min(end,middle));
		int y=maxsegment(now*2+1,middle+1,right,max(begin,middle+1),end);
		if ((x>=1)&&(y>=1))
		{
			if (ar[x]<ar[y]) return y;
			else return x;
		}
		else if (x>=1)
		{
			return x;
		}
		else
		{
			return y;
		}
	}
}
int main()
{
	int k;
    cin>>n;
    ar.resize(n+1);
    segment_tree.resize(4*n+1);
    for (int i=1;i<=n;i++)
    {
    	cin>>ar[i];
    }
    build(1,1,n);
    cin>>k;
    for (int i=1;i<=k;i++)
    {
    	int l,r;
    	cin>>l>>r;
    	int x=maxsegment(1,1,n,l,r);
    	cout<<ar[x]<<" "<<x<<"\n";
    }
    return 0;
}
